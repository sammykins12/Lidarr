import React, { PropTypes } from 'react';
import classNames from 'classnames';
import { kinds, sizes } from 'Helpers/Props';
import styles from './Label.css';

function Label(props) {
  const {
    className,
    kind,
    size,
    children,
    ...otherProps
  } = props;

  return (
    <span
      className={classNames(
        className,
        styles[kind],
        styles[size]
      )}
      {...otherProps}
    >
      {children}
    </span>
  );
}

Label.propTypes = {
  className: PropTypes.string.isRequired,
  kind: PropTypes.oneOf(kinds.all).isRequired,
  size: PropTypes.oneOf(sizes.all).isRequired,
  children: PropTypes.node.isRequired
};

Label.defaultProps = {
  className: styles.label,
  kind: kinds.DEFAULT,
  size: sizes.SMALL
};

export default Label;